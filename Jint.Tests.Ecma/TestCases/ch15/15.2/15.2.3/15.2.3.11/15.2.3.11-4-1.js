/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.11/15.2.3.11-4-1.js
 * @description Object.isSealed returns false for all built-in objects (Global)
 */


function testcase() {
  // in non-strict mode, 'this' is bound to the global object.
  var b = Object.isSealed(this);
  if (b === false) {
    return true;
  }
 }
runTestCase(testcase);
