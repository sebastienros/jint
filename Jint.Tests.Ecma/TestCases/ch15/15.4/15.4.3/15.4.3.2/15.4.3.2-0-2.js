/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-2.js
 * @description Array.isArray must exist as a function taking 1 parameter
 */


function testcase() {
  if (Array.isArray.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
