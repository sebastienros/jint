/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-145.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Date.prototype.setUTCFullYear)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Date.prototype, "setUTCFullYear");
  if (desc.value === Date.prototype.setUTCFullYear &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
