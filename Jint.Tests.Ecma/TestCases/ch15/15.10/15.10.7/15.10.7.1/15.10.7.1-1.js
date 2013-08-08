/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.1/15.10.7.1-1.js
 * @description RegExp.prototype.source is of type String
 */


function testcase() {
  return (typeof(RegExp.prototype.source)) === 'string';
 }
runTestCase(testcase);
