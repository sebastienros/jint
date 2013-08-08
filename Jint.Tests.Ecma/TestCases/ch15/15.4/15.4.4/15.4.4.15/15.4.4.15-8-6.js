/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-6.js
 * @description Array.prototype.lastIndexOf must return correct index(null)
 */


function testcase() {
  var obj = {toString:function (){return null}};
  var _null = null;
  var a = new Array(true,undefined,0,false,null,1,"str",0,1,null,true,false,undefined,_null,"null",undefined,"str",obj);
  if (a.lastIndexOf(null) === 13 ) 
  {
    return true;
  }
 }
runTestCase(testcase);
