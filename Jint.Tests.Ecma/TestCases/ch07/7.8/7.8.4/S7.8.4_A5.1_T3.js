// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * EscapeSequence :: 0
 *
 * @path ch07/7.8/7.8.4/S7.8.4_A5.1_T3.js
 * @description "\x00"
 */

//CHECK#1
if ("\x00" !== "\0") {
  $ERROR('#1: "\\x00" === "\\0"');
}

