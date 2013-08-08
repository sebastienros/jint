// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * EscapeSequence :: 0
 *
 * @path ch07/7.8/7.8.4/S7.8.4_A5.1_T2.js
 * @description "\u0000"
 */

//CHECK#1
if ("\u0000" !== "\0") {
  $ERROR('#1: "\\u0000" === "\\0"');
}

