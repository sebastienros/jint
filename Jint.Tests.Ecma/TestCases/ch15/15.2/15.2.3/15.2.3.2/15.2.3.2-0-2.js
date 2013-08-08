/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-0-2.js
 * @description Object.getPrototypeOf must exist as a function taking 1 parameter
 */


function testcase() {
  if (Object.getPrototypeOf.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
