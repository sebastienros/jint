/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.44/15.9.5.44-0-2.js
 * @description Date.prototype.toJSON must exist as a function taking 1 parameter
 */


function testcase() {
  if (Date.prototype.toJSON.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
