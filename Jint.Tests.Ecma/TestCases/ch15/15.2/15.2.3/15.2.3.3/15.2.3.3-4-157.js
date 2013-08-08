/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-157.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Date.prototype.toDateString)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Date.prototype, "toDateString");
  if (desc.value === Date.prototype.toDateString &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
