/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3_2-2-b-i-1.js
 * @description JSON.stringify converts string wrapper objects returned from a toJSON call to literal strings.
 */


function testcase() {
  var obj = {
    prop:42,
    toJSON: function () {return 'fortytwo objects'}
    };
  return JSON.stringify([obj]) === '["fortytwo objects"]';
  }
runTestCase(testcase);
