/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-56.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Array.prototype.forEach)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Array.prototype, "forEach");
  if (desc.value === Array.prototype.forEach &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
