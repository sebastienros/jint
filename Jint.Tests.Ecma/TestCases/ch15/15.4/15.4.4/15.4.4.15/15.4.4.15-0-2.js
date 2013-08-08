/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-0-2.js
 * @description Array.prototype.lastIndexOf has a length property whose value is 1.
 */


function testcase() {
  if (Array.prototype.lastIndexOf.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
