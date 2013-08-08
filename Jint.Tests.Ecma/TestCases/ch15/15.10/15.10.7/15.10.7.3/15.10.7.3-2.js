/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.3/15.10.7.3-2.js
 * @description RegExp.prototype.ignoreCase is a data property with default attribute values (false)
 */


function testcase() {
  var d = Object.getOwnPropertyDescriptor(RegExp.prototype, 'ignoreCase');
  
  if (d.writable === false &&
      d.enumerable === false &&
      d.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
