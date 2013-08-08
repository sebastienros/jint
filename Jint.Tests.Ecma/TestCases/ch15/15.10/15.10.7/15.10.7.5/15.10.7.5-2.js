/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.5/15.10.7.5-2.js
 * @description RegExp.prototype.lastIndex is a data property with specified attribute values
 */


function testcase() {
  var d = Object.getOwnPropertyDescriptor(RegExp.prototype, 'lastIndex');
  
  if (d.writable === true &&
      d.enumerable === false &&
      d.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
