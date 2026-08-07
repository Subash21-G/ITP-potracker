import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { PurchaseOrderDetails } from '../models/purchase-order-details.model';
import { PurchaseOrderListResponse } from '../models/purchase-order.model';
import { PurchaseOrdersApiService } from './purchase-orders-api.service';

describe('PurchaseOrdersApiService', () => {
  let service: PurchaseOrdersApiService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PurchaseOrdersApiService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('requests a filtered, paginated Purchase Order page', () => {
    const response: PurchaseOrderListResponse = {
      items: [],
      summary: {
        totalPurchaseOrders: 0,
        openPurchaseOrders: 0,
        completedPurchaseOrders: 0,
        purchaseOrdersWithPendingMaterials: 0,
        purchaseOrdersWithOverdueMaterials: 0,
        purchaseOrdersWithoutHandler: 0,
        totalPOQuantity: 0,
        totalInvoiceBalanceQuantity: 0,
        totalPendingDispatchQuantity: 0,
      },
      handlers: ['Manoj S'],
      page: 2,
      pageSize: 25,
      totalRecords: 30,
      totalPages: 2,
      hasPreviousPage: true,
      hasNextPage: false,
    };

    service
      .getPurchaseOrders({
        page: 2,
        pageSize: 25,
        sortColumn: 'delivery',
        sortDirection: 'desc',
        search: '4500',
        handler: 'Manoj S',
        status: 'Open',
        poDateFrom: '2026-01-01',
        minMaterials: '2',
        maxMaterials: '20',
        minPoQuantity: '10',
        maxPoQuantity: '500',
        minPoValue: '1000',
        maxPoValue: '500000',
        minDispatchBalance: '1',
        maxDispatchBalance: '200',
        deliveryDateFrom: '2026-02-01',
        deliveryDateTo: '2026-12-31',
        pdfAvailability: 'available',
      })
      .subscribe((result) => expect(result).toEqual(response));

    const request = httpController.expectOne(
      (candidate) => candidate.url === '/api/purchase-orders',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');

    expect(
      request.request.params.get(
        'sortColumn',
      ),
    ).toBe('delivery');

    expect(
      request.request.params.get(
        'sortDirection',
      ),
    ).toBe('desc');

    expect(request.request.params.get('search')).toBe('4500');
    expect(request.request.params.get('handler')).toBe('Manoj S');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('poDateFrom')).toBe('2026-01-01');
    expect(request.request.params.get('minMaterials')).toBe('2');
    expect(request.request.params.get('maxMaterials')).toBe('20');
    expect(request.request.params.get('minPoQuantity')).toBe('10');
    expect(request.request.params.get('maxPoQuantity')).toBe('500');
    expect(request.request.params.get('minPoValue')).toBe('1000');
    expect(request.request.params.get('maxPoValue')).toBe('500000');
    expect(request.request.params.get('minDispatchBalance')).toBe('1');
    expect(request.request.params.get('maxDispatchBalance')).toBe('200');
    expect(request.request.params.get('deliveryDateFrom')).toBe('2026-02-01');
    expect(request.request.params.get('deliveryDateTo')).toBe('2026-12-31');
    expect(request.request.params.get('pdfAvailability')).toBe('available');
    expect(request.request.withCredentials).toBe(true);

    request.flush(response);
  });

  it('requests one Purchase Order with a paginated material page', () => {
    const response: PurchaseOrderDetails = {
      purchaseOrderId: 19,
      poNumber: '4500123456',
      poDate: '2026-01-01T00:00:00',
      poReceivedDate: '2026-01-02T00:00:00',
      handler: 'Manoj S',
      purchaserName: 'WEG Industries (India) PVT LTD',
      buyerEmail: null,
      vendorCode: null,
      vendorName: null,
      paymentTerms: null,
      deliveryCondition: null,
      currency: 'INR',
      basicValue: 0,
      taxValue: 0,
      grandTotal: 0,
      importFormat: 'WEG',
      pdfFilePath: null,
      status: 'Open',
      createdDate: '2026-01-02T00:00:00',
      updatedDate: null,
      summary: {
        totalMaterials: 30,
        completedMaterials: 5,
        pendingMaterials: 25,
        overdueMaterials: 2,
        totalPOQuantity: 100,
        totalInvoiceBalance: 80,
        totalDispatchBalance: 70,
      },
      materials: {
        items: [],
        page: 2,
        pageSize: 10,
        totalRecords: 30,
        totalPages: 3,
        hasPreviousPage: true,
        hasNextPage: true,
      },
    };

    service
      .getPurchaseOrderDetails(19, 2, 10)
      .subscribe((result) => expect(result).toEqual(response));

    const request = httpController.expectOne(
      (candidate) => candidate.url === '/api/purchase-orders/19',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('materialPage')).toBe('2');
    expect(request.request.params.get('materialPageSize')).toBe('10');
    expect(request.request.withCredentials).toBe(true);

    request.flush(response);
  });
});
