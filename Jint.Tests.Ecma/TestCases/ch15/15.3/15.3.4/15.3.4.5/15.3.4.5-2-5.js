/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.3.4.5 step 2 specifies that a TypeError must be thrown if the Target is not callable.
 *
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-5.js
 * @description Function.prototype.bind allows Target to be a constructor (Boolean)
 */


function testcase() {
  var bbc = Boolean.bind(null);
  var b = bbc(true);
  if (b === true) {
    return true;
  }
 }
runTestCase(testcase);
