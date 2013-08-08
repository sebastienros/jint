// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Punctuator cannot be expressed as a Unicode escape sequence consisting of six characters, namely \u plus four hexadecimal digits
 *
 * @path ch07/7.7/S7.7_A2_T5.js
 * @description Try to use . as a Unicode \u002E
 * @negative
 */

x = 1;
this\u002Ex;

