/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-1.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Boolean)
 */


function testcase() {
  if (Object.getPrototypeOf(Boolean) === Function.prototype) {
    return true;
  }
 }
runTestCase(testcase);
