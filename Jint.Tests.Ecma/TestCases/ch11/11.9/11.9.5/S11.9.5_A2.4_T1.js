// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * First expression is evaluated first, and then second expression
 *
 * @path ch11/11.9/11.9.5/S11.9.5_A2.4_T1.js
 * @description Checking with "="
 */

//CHECK#1
var x = 0; 
if ((x = 1) !== x) {
  $ERROR('#1: var x = 0; (x = 1) === x');
}

//CHECK#2
var x = 0; 
if (!(x !== (x = 1))) {
  $ERROR('#2: var x = 0; x !== (x = 1)');
}


