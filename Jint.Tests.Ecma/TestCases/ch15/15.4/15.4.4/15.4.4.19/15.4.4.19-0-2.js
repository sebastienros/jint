/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-0-2.js
 * @description Array.prototype.map.length must be 1
 */


function testcase() {
  if (Array.prototype.map.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
