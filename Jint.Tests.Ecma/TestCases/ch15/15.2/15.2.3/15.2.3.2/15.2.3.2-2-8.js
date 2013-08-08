/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-8.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Math)
 */


function testcase() {
  if (Object.getPrototypeOf(Math) === Object.prototype) {
    return true;
  }
 }
runTestCase(testcase);
