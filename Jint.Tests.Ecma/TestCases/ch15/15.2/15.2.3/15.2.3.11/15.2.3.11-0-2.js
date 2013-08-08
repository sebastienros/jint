/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-0-2.js
 * @description Object.isSealed must exist as a function taking 1 parameter
 */


function testcase() {
  if (Object.isSealed.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
