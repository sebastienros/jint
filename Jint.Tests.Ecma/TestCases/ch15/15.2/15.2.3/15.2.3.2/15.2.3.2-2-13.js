/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-13.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (RangeError)
 */


function testcase() {
  if (Object.getPrototypeOf(RangeError) === Function.prototype) {
    return true;
  }
 }
runTestCase(testcase);
