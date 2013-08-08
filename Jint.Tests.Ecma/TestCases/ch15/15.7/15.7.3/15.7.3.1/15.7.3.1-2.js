/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.7/15.7.3/15.7.3.1/15.7.3.1-2.js
 * @description Number.prototype, initial value is the Number prototype object
 */


function testcase() {
  // assume that Number.prototype has not been modified.
  return Object.getPrototypeOf(new Number(42))===Number.prototype;
 }
runTestCase(testcase);
