/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-0-2.js
 * @description Array.prototype.some.length must be 1
 */


function testcase() {
  if (Array.prototype.some.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
