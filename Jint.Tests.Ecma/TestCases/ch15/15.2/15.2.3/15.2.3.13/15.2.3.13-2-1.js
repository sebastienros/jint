/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-1.js
 * @description Object.isExtensible returns true for all built-in objects (Global)
 */

global = this;
function testcase() {
  // in non-strict mode, 'this' is bound to the global object.
  var e = Object.isExtensible(this);
  if (e === true) {
    return true;
  }
 }
runTestCase(testcase);
