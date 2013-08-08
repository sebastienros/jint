/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.2/15.10.7.2-2.js
 * @description RegExp.prototype.global is a data property with default attribute values (false)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(RegExp.prototype, 'global');
  
  if (desc.writable === false &&
      desc.enumerable === false &&
      desc.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
