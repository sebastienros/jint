/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3_2-2-b-i-3.js
 * @description JSON.stringify converts Boolean wrapper objects returned from a toJSON call to literal Boolean values.
 */


function testcase() {
  var obj = {
    prop:42,
    toJSON: function () {return new Boolean(true)}
    };
  return JSON.stringify([obj]) === '[true]';
  }
runTestCase(testcase);
