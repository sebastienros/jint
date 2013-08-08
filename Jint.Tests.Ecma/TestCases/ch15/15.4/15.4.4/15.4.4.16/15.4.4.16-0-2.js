/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-0-2.js
 * @description Array.prototype.every.length must be 1
 */


function testcase() {
  if (Array.prototype.every.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
