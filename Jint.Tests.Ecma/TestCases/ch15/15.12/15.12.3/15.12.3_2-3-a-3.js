/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3_2-3-a-3.js
 * @description JSON.stringify converts Boolean wrapper objects returned from replacer functions to literal numbers.
 */


function testcase() {
  return JSON.stringify([42], function(k,v) {return v===42? new Boolean(false):v}) === '[false]';
  }
runTestCase(testcase);
