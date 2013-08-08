/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-0-2.js
 * @description Array.prototype.forEach.length must be 1
 */


function testcase() {
  if (Array.prototype.forEach.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
