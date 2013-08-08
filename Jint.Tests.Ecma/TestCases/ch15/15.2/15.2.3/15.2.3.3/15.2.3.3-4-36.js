/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-36.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Function.prototype.apply)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Function.prototype, "apply");
  if (desc.value === Function.prototype.apply &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
