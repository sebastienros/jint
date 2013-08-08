/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.1/15.1.1.2/15.1.1.2-0.js
 * @description Global.Infinity is a data property with default attribute values (false)
 */


function testcase() {
    var desc = Object.getOwnPropertyDescriptor(fnGlobalObject(), 'Infinity');
  if (desc.writable === false &&
      desc.enumerable === false &&
      desc.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
