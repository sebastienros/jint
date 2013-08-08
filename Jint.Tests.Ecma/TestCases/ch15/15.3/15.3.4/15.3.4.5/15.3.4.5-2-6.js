/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.3.4.5 step 2 specifies that a TypeError must be thrown if the Target is not callable.
 *
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-6.js
 * @description Function.prototype.bind allows Target to be a constructor (Object)
 */


function testcase() {
  var boc = Object.bind(null);
  var o = boc(42);
  if (o == 42) {
    return true;
  }
 }
runTestCase(testcase);
