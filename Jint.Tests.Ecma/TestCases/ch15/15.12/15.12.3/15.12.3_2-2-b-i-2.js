/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3_2-2-b-i-2.js
 * @description JSON.stringify converts Number wrapper objects returned from a toJSON call to literal Number.
 */


function testcase() {
  var obj = {
    prop:42,
    toJSON: function () {return new Number(42)}
    };
  return JSON.stringify([obj]) === '[42]';
  }
runTestCase(testcase);
