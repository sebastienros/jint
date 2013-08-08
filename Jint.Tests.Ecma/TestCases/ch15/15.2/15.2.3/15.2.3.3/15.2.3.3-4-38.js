/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-38.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Function.prototype.bind)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Function.prototype, "bind");
  if (desc.value === Function.prototype.bind &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
