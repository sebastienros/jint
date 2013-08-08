/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-180.js
 * @description Object.getOwnPropertyDescriptor returns data desc (all false) for properties on built-ins (Global.undefined)
 */


function testcase() {
  // in non-strict mode, 'this' is bound to the global object.
  var desc = Object.getOwnPropertyDescriptor(fnGlobalObject(),  "undefined");

  if (desc.writable === false &&
      desc.enumerable === false &&
      desc.configurable === false &&
      desc.hasOwnProperty('get') === false &&
      desc.hasOwnProperty('set') === false) {
    return true;
  }
  return false;
 }
runTestCase(testcase);
