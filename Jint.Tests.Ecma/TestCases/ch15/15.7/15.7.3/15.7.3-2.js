/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.7/15.7.3/15.7.3-2.js
 * @description Number constructor - [[Prototype]] is the Function prototype object (using getPrototypeOf)
 */


function testcase() {
  var p = Object.getPrototypeOf(Number);
  if (p === Function.prototype) {
    return true;
  }
 }
runTestCase(testcase);
