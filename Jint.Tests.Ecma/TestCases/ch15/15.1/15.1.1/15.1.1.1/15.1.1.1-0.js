/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.1/15.1.1.1/15.1.1.1-0.js
 * @description Global.NaN is a data property with default attribute values (false)
 */


function testcase() {
    var desc = Object.getOwnPropertyDescriptor(fnGlobalObject(), 'NaN');
  if (desc.writable === false &&
      desc.enumerable === false &&
      desc.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
