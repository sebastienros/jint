// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Line Terminator cannot be expressed as a Unicode escape sequence consisting of six characters, namely \u plus four hexadecimal digits
 *
 * @path ch07/7.3/S7.3_A6_T3.js
 * @description Insert LINE SEPARATOR (U+2028) in var x
 * @negative
 */

var\u2028x;

