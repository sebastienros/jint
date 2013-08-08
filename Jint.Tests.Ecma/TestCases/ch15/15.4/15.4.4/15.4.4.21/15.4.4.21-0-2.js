/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-0-2.js
 * @description Array.prototype.reduce.length must be 1
 */


function testcase() {
  if (Array.prototype.reduce.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
