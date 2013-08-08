/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.4/15.10.7.4-2.js
 * @description RegExp.prototype.multiline is a data property with default attribute values (false)
 */


function testcase() {
  var d = Object.getOwnPropertyDescriptor(RegExp.prototype, 'multiline');
  
  if (d.writable === false &&
      d.enumerable === false &&
      d.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
