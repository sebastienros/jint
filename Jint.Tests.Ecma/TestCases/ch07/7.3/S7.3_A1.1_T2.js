// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * LINE FEED (U+000A) may occur between any two tokens
 *
 * @path ch07/7.3/S7.3_A1.1_T2.js
 * @description Insert real LINE FEED between tokens of var x=1
 */

//CHECK#1
var
x
=
1;
if (x !== 1) {
  $ERROR('#1: var\\nx\\n=\\n1\\n; x === 1. Actual: ' + (x));
}

