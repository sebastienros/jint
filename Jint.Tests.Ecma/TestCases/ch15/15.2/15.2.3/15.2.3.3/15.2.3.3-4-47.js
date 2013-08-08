/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-47.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Array.prototype.pop)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Array.prototype, "pop");
  if (desc.value === Array.prototype.pop &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
