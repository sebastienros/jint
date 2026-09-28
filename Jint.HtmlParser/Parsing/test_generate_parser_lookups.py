import json
from pathlib import Path
import re
import unittest

import generate_parser_lookups as parser


class ParserLookupTests(unittest.TestCase):
    def test_generated_files_are_current(self):
        for path, content in parser.outputs().items():
            self.assertEqual(path.read_text(), content, str(path))

    def test_every_vocabulary_is_reachable_from_parser_code(self):
        directory = Path(__file__).resolve().parent.parent
        specs = json.loads((directory / "Parsing/parser-lookups.json").read_text())
        source = "\n".join(path.read_text() for path in directory.rglob("*.cs")
                           if not path.name.endswith(".g.cs"))
        reached = set()
        while True:
            previous = len(reached)
            for spec in specs:
                if spec["name"] in reached:
                    continue
                if re.search(r"\b" + spec["name"] + r"\.Match\b", source) or (
                        "keywordSet" in spec and re.search(
                            r"\bCssKeywordSet\." + spec["keywordSet"] + r"\b", source)):
                    reached.add(spec["name"])
                    source += "\n" + "\n".join(parser.entries(spec, specs).values())
            if len(reached) == previous:
                break
        self.assertEqual(set(spec["name"] for spec in specs) - reached, set())

    def test_every_path_proves_all_input_units(self):
        specs = json.loads((parser.DIRECTORY / "parser-lookups.json").read_text())

        def check(node, length, proven=frozenset()):
            if node.name is not None:
                for test in node.checks:
                    units = frozenset(range(test.offset, test.offset + test.width))
                    self.assertFalse(units & proven)
                    proven |= units
                self.assertEqual(proven, frozenset(range(length)))
                return
            self.assertLessEqual(node.offset + node.width, length)
            units = frozenset(range(node.offset, node.offset + node.width))
            for _, child in node.children:
                check(child, length, proven | units)

        for spec in specs:
            for length, node in parser.generator.trees(
                    list(parser.entries(spec, specs)), "char", spec.get("ignoreCase", False)).items():
                check(node, length)


if __name__ == "__main__":
    unittest.main()
