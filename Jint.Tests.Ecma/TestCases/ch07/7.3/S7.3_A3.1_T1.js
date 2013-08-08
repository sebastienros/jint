// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Single line comments can not contain LINE FEED (U+000A) inside
 *
 * @path ch07/7.3/S7.3_A3.1_T1.js
 * @description Insert LINE FEED (\u000A) into single line comment
 * @negative
 */

// CHECK#1
eval("// single line \u000A comment");

