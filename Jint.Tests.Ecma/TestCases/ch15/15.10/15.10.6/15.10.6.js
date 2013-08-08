/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.6/15.10.6.js
 * @description RegExp.prototype is itself a RegExp
 */


function testcase() {
  var s = Object.prototype.toString.call(RegExp.prototype);
  return s === '[object RegExp]';
 }
runTestCase(testcase);
