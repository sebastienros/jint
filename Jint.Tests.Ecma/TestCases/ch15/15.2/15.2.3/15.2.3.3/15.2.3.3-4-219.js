/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-219.js
 * @description Object.getOwnPropertyDescriptor returns data desc (all false) for properties on built-ins (ReferenceError.prototype)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(ReferenceError, "prototype");

  if (desc.writable === false &&
      desc.enumerable === false &&
      desc.configurable === false &&
      desc.hasOwnProperty('get') === false &&
      desc.hasOwnProperty('set') === false) {
    return true;
  }
 }
runTestCase(testcase);
