/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-58.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Array.prototype.filter)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Array.prototype, "filter");
  if (desc.value === Array.prototype.filter &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
