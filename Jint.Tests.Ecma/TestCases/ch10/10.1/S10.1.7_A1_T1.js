// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The this value associated with an executioncontext is immutable
 *
 * @path ch10/10.1/S10.1.7_A1_T1.js
 * @description Checking if deleting "this" fails
 */

//CHECK#1
if (delete this !== true)
  $ERROR('#1: The this value associated with an executioncontext is immutable. Actual: this was deleted');
  

