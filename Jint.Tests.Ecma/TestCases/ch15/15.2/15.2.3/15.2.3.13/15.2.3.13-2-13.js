/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-13.js
 * @description Object.isExtensible returns true for all built-in objects (Function.constructor)
 */


function testcase() {
  var e = Object.isExtensible(Function.constructor);
  if (e === true) {
    return true;
  }
 }
runTestCase(testcase);
