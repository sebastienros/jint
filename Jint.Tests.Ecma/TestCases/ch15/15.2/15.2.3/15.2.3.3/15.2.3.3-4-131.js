/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-131.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Date.prototype.getUTCDay)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Date.prototype, "getUTCDay");
  if (desc.value === Date.prototype.getUTCDay &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
