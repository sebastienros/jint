/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-0-2.js
 * @description Object.defineProperty must exist as a function taking 3 parameters
 */


function testcase() {
  if (Object.defineProperty.length === 3) {
    return true;
  }
 }
runTestCase(testcase);
