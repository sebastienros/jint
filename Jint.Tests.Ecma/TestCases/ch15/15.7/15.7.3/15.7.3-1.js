/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.7/15.7.3/15.7.3-1.js
 * @description Number constructor - [[Prototype]] is the Function prototype object
 */


function testcase() {
  if (Function.prototype.isPrototypeOf(Number) === true) {
    return true;
  }
 }
runTestCase(testcase);
