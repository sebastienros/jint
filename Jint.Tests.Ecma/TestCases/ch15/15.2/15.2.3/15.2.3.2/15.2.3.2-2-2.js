/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Let 'x' be the return value from getPrototypeOf when called on d.
 * Then, x.isPrototypeOf(d) must be true.
 *
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-2.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (custom object)
 */


function testcase() {
  function base() {}

  function derived() {}
  derived.prototype = new base();

  var d = new derived();
  var x = Object.getPrototypeOf(d);
  if (x.isPrototypeOf(d) === true) {
    return true;
  }
 }
runTestCase(testcase);
