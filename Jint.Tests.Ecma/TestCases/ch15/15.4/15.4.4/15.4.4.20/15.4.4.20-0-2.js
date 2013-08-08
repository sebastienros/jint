/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-0-2.js
 * @description Array.prototype.filter.length must be 1
 */


function testcase() {
  if (Array.prototype.filter.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
