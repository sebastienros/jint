/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-17.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (URIError)
 */


function testcase() {
  if (Object.getPrototypeOf(URIError) === Function.prototype) {
    return true;
  }
 }
runTestCase(testcase);
