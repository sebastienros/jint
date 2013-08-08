/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-9.js
 * @description Function.prototype.bind allows Target to be a constructor (Date)
 */


function testcase() {
  var bdc = Date.bind(null);
  var s = bdc(0, 0, 0);
  if (typeof(s) === 'string') {
    return true;
  }
 }
runTestCase(testcase);
