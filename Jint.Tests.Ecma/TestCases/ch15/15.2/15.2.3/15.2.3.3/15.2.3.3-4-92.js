/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-92.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Number.prototype.toExponential)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Number.prototype, "toExponential");
  if (desc.value === Number.prototype.toExponential &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
