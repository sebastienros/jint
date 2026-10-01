import itertools
import unittest

import generate_known_names as generator


class DecisionTreeTests(unittest.TestCase):
    def test_every_leaf_verifies_exactly_the_remaining_units(self):
        def visit(node, length, proven=frozenset()):
            if node.name is not None:
                for check in node.checks:
                    covered = frozenset(range(check.offset, check.offset + check.width))
                    self.assertFalse(covered & proven)
                    self.assertLessEqual(check.offset + check.width, length)
                    proven |= covered
                self.assertEqual(proven, frozenset(range(length)))
            else:
                self.assertLessEqual(node.offset + node.width, length)
                covered = frozenset(range(node.offset, node.offset + node.width))
                self.assertTrue(covered - proven)
                for _, child in node.children:
                    visit(child, length, proven | covered)

        for names, kind, ignore_case in itertools.product(
                [generator.HTML_NAMES, generator.HEADERS, generator.KEYWORDS, generator.SHARED_PREFIXES,
                 generator.PUNCTUATION_COLLISIONS],
                ["byte", "char"], [False, True]):
            for length, root in generator.trees(names, kind, ignore_case).items():
                visit(root, length)

    def test_shared_prefix_is_not_the_first_discriminator(self):
        root, = generator.trees(generator.SHARED_PREFIXES, "char", False).values()
        self.assertGreaterEqual(root.offset, len("shared-prefix-") - 3)
        self.assertGreater(len(root.children), 1)

    def test_output_does_not_depend_on_input_order(self):
        for kind, ignore_case in itertools.product(["char", "byte"], [False, True]):
            forward = generator.emit_class(generator.HTML_NAMES, "Names", kind, ignore_case)
            backward = generator.emit_class(list(reversed(generator.HTML_NAMES)), "Names", kind, ignore_case)
            self.assertEqual(forward, backward)

    def test_case_masks_preserve_punctuation_and_high_utf16_bits(self):
        check = generator.comparison("a-1", 0, 3, frozenset(), 16, True)
        self.assertEqual(check.mask, 0xFFFFFFFFFFDF)
        self.assertEqual(check.expected, 0x0031002D0041)
        check = generator.comparison("a-1", 0, 3, frozenset(), 8, True)
        self.assertEqual(check.mask, 0xFFFFDF)
        self.assertEqual(check.expected, 0x312D41)

    def test_proven_positions_are_masked_out_of_later_discriminators(self):
        check = generator.comparison("abcd", 0, 4, frozenset([1, 3]), 16, False)
        self.assertEqual(check.mask, 0x0000FFFF0000FFFF)
        self.assertEqual(check.expected, 0x0000006300000061)

    def test_both_native_utf16_endiannesses_normalize_to_the_same_constant(self):
        for text in ("ab", "abcd"):
            expected = generator.comparison(text, 0, len(text), frozenset(), 16, False).expected
            little = int.from_bytes(text.encode("utf-16-le"), "little")
            big = int.from_bytes(text.encode("utf-16-be"), "big")
            if len(text) == 2:
                normalized = ((big << 16) | (big >> 16)) & 0xFFFFFFFF
            else:
                normalized = ((big << 48) | ((big & 0xFFFF0000) << 16) |
                              ((big >> 16) & 0xFFFF0000) | (big >> 48)) & 0xFFFFFFFFFFFFFFFF
            self.assertEqual(little, expected)
            self.assertEqual(normalized, expected)

    def test_invalid_vocabularies_fail_instead_of_generating_partial_matches(self):
        for names, ignore_case in [([], False), ([""], False), (["a", "a"], False),
                                   (["a", "A"], True), (["\u0100"], False)]:
            with self.assertRaises(ValueError):
                generator.trees(names, "char", ignore_case)

    def test_typed_results_and_default_are_emitted_without_a_runtime_vocabulary(self):
        output = "\n".join(generator.emit_class(
            ["yes", "no"], "BooleanNames", results={"yes": "true", "no": "false"},
            return_type="bool?", default_result="null", include_values=False))
        self.assertIn("bool? Match(ReadOnlySpan<char> input)", output)
        self.assertIn("return true;", output)
        self.assertIn("return false;", output)
        self.assertIn("return null;", output)
        self.assertNotIn("string[]", output)
        self.assertIn("const int Count = 2;", output)

    def test_typed_results_must_cover_exactly_the_vocabulary(self):
        for results in ({}, {"yes": "true"}, {"yes": "true", "no": "false", "maybe": "null"}):
            with self.assertRaises(ValueError):
                generator.emit_class(["yes", "no"], "Names", results=results)

    def test_ascii_insensitive_nodes_switch_on_the_common_mask(self):
        output = "\n".join(generator.emit_class(generator.PUNCTUATION_COLLISIONS, "Names", "char", True))
        self.assertIn("switch (chunk0 & 0xFFDFFFDFU)", output)
        self.assertNotIn("else if", output)
        # '@' and '`' share a case, so each re-checks bit 0x20 of its own unit.
        self.assertIn("if ((chunk0 & 0xFFFFFFDFU) == 0x00400058U)", output)
        self.assertIn("if ((chunk0 & 0xFFFFFFDFU) == 0x00600058U)", output)

    def test_heavy_lengths_move_into_their_own_methods(self):
        light = "\n".join(generator.emit_class(generator.KEYWORDS, "Names"))
        self.assertNotRegex(light, r"Match\d+\(")
        names = [f"{a}{b}{c}-{d}" for a in "abcd" for b in "efgh" for c in "ijkl" for d in "mn"] + ["tiny"]
        output = "\n".join(generator.emit_class(names, "Names"))
        self.assertIn("case 4:\n            {", output)
        self.assertIn("case 5: return Match5(input);", output)
        self.assertIn("private static string? Match5(ReadOnlySpan<char> input)", output)


if __name__ == "__main__":
    unittest.main()
