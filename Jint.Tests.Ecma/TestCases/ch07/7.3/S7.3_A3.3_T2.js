// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Single line comments can not contain LINE SEPARATOR (U+2028) inside
 *
 * @path ch07/7.3/S7.3_A3.3_T2.js
 * @description Insert LINE SEPARATOR (\u2028) into begin of single line comment
 * @negative
 */

// CHECK#1
eval("//\u2028 single line comment");

