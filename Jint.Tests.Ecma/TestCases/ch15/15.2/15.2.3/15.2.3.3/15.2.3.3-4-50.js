/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-50.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Array.prototype.splice)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Array.prototype, "splice");
  if (desc.value === Array.prototype.splice &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
