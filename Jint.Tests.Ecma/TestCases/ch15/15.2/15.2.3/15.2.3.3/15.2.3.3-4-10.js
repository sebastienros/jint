/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-10.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Global.decodeURIComponent)
 */


function testcase() {
  var global = fnGlobalObject();
  var desc = Object.getOwnPropertyDescriptor(global,  "decodeURIComponent");
  if (desc.value === global.decodeURIComponent &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
