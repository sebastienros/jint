/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.5/15.4.5-1.js
 * @description Array instances have [[Class]] set to 'Array'
 */


function testcase() {
  var a = [];
  var s = Object.prototype.toString.call(a);
  if (s === '[object Array]') {
    return true;
  }
 }
runTestCase(testcase);
