"""Small failure probes for the locked dependency inventory."""

import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("html_parser_inventory", Path(__file__).with_name("inventory.py"))
inventory = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inventory)


class InventoryTests(unittest.TestCase):
    def test_text_hash_and_source_inventory_ignore_checkout_line_endings(self):
        self.assertEqual(inventory.digest(b"a\nb\n"), inventory.digest(b"a\r\nb\r\n"))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "Jint.Browser/Dom/Example.cs"
            source.parent.mkdir(parents=True)
            source.write_bytes(b"using AngleSharp.Dom;\nclass Example {}\n")
            with patch.object(inventory, "ROOT", root):
                first = inventory.source_inventory()
                source.write_bytes(b"using AngleSharp.Dom;\r\nclass Example {}\r\n")
                self.assertEqual(first, inventory.source_inventory())

    def test_new_angle_sharp_file_and_changed_unqualified_use_move_the_lock(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "Jint.Browser/Dom/Example.cs"
            source.parent.mkdir(parents=True)
            source.write_text("using AngleSharp.Dom;\nclass Example { INode Node; }\n")
            with patch.object(inventory, "ROOT", root):
                first = inventory.source_inventory()
                self.assertEqual(["Jint.Browser/Dom/Example.cs"], [item["path"] for item in first])
                self.assertEqual("B1:dom-binding", first[0]["owner"])
                source.write_text("using AngleSharp.Dom;\nclass Example { IElement Node; }\n")
                self.assertNotEqual(first, inventory.source_inventory())
                other = root / "Jint.Browser/Runtime/NewConsumer.cs"
                other.parent.mkdir(parents=True)
                other.write_text("using AngleSharp;\n")
                self.assertEqual(2, len(inventory.source_inventory()))

    def test_unknown_owner_fails_instead_of_being_silently_assigned(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "NewProduction/Consumer.cs"
            source.parent.mkdir(parents=True)
            source.write_text("using AngleSharp.Dom;\n")
            with patch.object(inventory, "ROOT", root):
                with self.assertRaisesRegex(ValueError, "No owner"):
                    inventory.source_inventory()

    def test_generated_members_are_named_and_owned(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            generated = root / "Jint.Browser/Dom/Generated"
            generated.mkdir(parents=True)
            (generated / "DomInterfaces.g.cs").write_text("internal static readonly DomInterfaceDefinition HTMLInputElement;\n")
            (generated / "DomShapes.Html.g.cs").write_text(
                'private static JsObjectShape BuildHTMLInputElement() => new Builder()\n'
                '    .PerRealmSlot(\n'
                '        global::Jint.Native.Symbol.GlobalSymbolRegistry.Iterator, iterator)\n'
                '    .Method("click", static (_, _) => null)\n'
                '    .Accessor("value", static (_, _) => null);\n'
                '    DomIterableMembers.ValueIterator(builder);\n'
            )
            overrides = root / "tools/dom-bindings/overrides.json"
            overrides.parent.mkdir(parents=True)
            overrides.write_text('{"skip": []}\n')
            extension = root / "Jint.Browser/Dom/Collections/DomIterableMembers.cs"
            extension.parent.mkdir(parents=True)
            extension.write_text('Add(builder, "entries"); Add(builder, "keys"); Add(builder, "values"); Add(builder, "forEach");')
            with patch.object(inventory, "ROOT", root):
                actual = inventory.generated_inventory()
            self.assertEqual({"click", "value", "@@iterator", "entries", "keys", "values", "forEach"},
                             {member["name"] for member in actual["members"]})
            self.assertEqual({"B2:form-controls"}, {member["owner"] for member in actual["members"]})

    def test_build_targets_and_arbitrary_generated_directory_are_scanned(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            targets = root / "Directory.Build.targets"
            targets.write_text('<Project><ItemGroup><PackageReference Include="AngleSharp.Css" Version="1.1.2" /></ItemGroup></Project>')
            generated = root / "Jint.Browser/Generated/NewConsumer.cs"
            generated.parent.mkdir(parents=True)
            generated.write_text("using AngleSharp.Dom;\n")
            with patch.object(inventory, "ROOT", root):
                files = inventory.source_inventory()
                self.assertEqual({"Directory.Build.targets", "Jint.Browser/Generated/NewConsumer.cs"},
                                 {item["path"] for item in files})
                props = root / "Directory.Packages.props"
                props.write_text('<Project><ItemGroup><PackageVersion Include="AngleSharp.Css" Version="1.1.2" /></ItemGroup></Project>')
                pin = root / "tools/dom-bindings/pin.json"
                pin.parent.mkdir(parents=True)
                pin.write_text('{"packages": {"AngleSharp.Css": "1.1.2"}}')
                refs = inventory.package_inventory()["references"]
                self.assertEqual([("Directory.Build.targets", "AngleSharp.Css")],
                                 [(item["path"], item["package"]) for item in refs])

    def test_comments_do_not_create_source_dependencies(self):
        self.assertEqual("\nusing Jint;\n", inventory.without_comments("// AngleSharp\nusing Jint;\n"))


if __name__ == "__main__":
    unittest.main()
