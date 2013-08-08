/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-0-2.js
 * @description String.prototype.trim must exist as a function taking 0 parameters
 */


function testcase() {
  if (String.prototype.trim.length === 0) {
    return true;
  }
 }
runTestCase(testcase);
